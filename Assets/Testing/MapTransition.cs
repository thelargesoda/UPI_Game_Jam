using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapTransition : MonoBehaviour
{
    [SerializeField] PolygonCollider2D mapBoundry;
    CinemachineConfiner confiner;
    [SerializeField] Direction direction;
    [SerializeField] float PanjangPos = 2;

    enum Direction { Up , Down, Left , Right }

    private void Awake()
    {
        confiner = FindObjectOfType<CinemachineConfiner>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Player"))
        {
            confiner.m_BoundingShape2D = mapBoundry;
            UpdatePosisiPlayer(collision.gameObject);
        }
    }

    private void UpdatePosisiPlayer(GameObject player)
    {
        Vector3 newPos = player.transform.position;

        switch (direction)
        {
            case Direction.Up:
                newPos.y += PanjangPos;
                break;
            case Direction.Down:
                newPos.y -= PanjangPos;
                break;
            case Direction.Left:
                newPos.x += PanjangPos;
                break;
            case Direction.Right:
                newPos.x -= PanjangPos;
                break;
        }
        player.transform.position = newPos;
    }
}
